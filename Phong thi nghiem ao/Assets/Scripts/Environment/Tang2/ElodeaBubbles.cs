using Khkt.Simulation.Bio;
using UnityEngine;

namespace Khkt.Environment.Tang2
{
    /// <summary>
    /// Bọt O₂ từ cành rong đuôi chó: tốc độ sinh bọt theo cường độ sáng (luật bình phương nghịch theo khoảng cách đèn).
    /// Dùng một bể 24 quả bọt dựng sẵn và một vật liệu dùng chung: không tạo vật thể / vật liệu mới lúc chạy (bản trước tạo
    /// mỗi bọt một vật liệu và một khối cầu có va chạm, để lâu bộ nhớ tăng dần gây giật). Chỉ chạy khi người chơi ở gần.
    /// </summary>
    public class ElodeaBubbles : MonoBehaviour
    {
        public Transform lampTransform;
        public Transform stemOrigin;
        public float lampPowerWatts = 60f;
        public float riseSpeed = 0.06f;
        public float riseHeight = 0.13f;
        public float activeDistance = 8f;

        const int Pool = 24;
        Transform[] bubbles;
        float[] age;
        float timer;
        Transform cam;
        static Material shared;

        void Start()
        {
            if (stemOrigin == null) { enabled = false; return; }
            if (shared == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                shared = new Material(sh) { name = "Bọt O2", color = new Color(0.9f, 0.97f, 1f, 1f) };
            }
            var mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            bubbles = new Transform[Pool];
            age = new float[Pool];
            for (int i = 0; i < Pool; i++)
            {
                var go = new GameObject("Bọt O2");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = shared;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                go.SetActive(false);
                bubbles[i] = go.transform;
                age[i] = -1;
            }
        }

        void Update()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam != null && (cam.position - stemOrigin.position).sqrMagnitude > activeDistance * activeDistance) return;
            float dt = Time.deltaTime;
            float dist = lampTransform != null ? Mathf.Max(0.05f, Vector3.Distance(lampTransform.position, stemOrigin.position)) : 0.3f;
            double perMinute = Photosynthesis.BubbleRatePerMinute(Photosynthesis.LightIntensityWattsPerM2(lampPowerWatts, dist));
            if (perMinute > 0.01)
            {
                timer += dt;
                float interval = (float)(60.0 / perMinute);
                if (timer >= interval) { timer = 0; Spawn(); }
            }
            for (int i = 0; i < Pool; i++)
            {
                if (age[i] < 0) continue;
                age[i] += dt;
                float y = age[i] * riseSpeed;
                var b = bubbles[i];
                b.position = stemOrigin.position + new Vector3(Mathf.Sin(age[i] * 9f + i) * 0.003f, y, 0);
                if (y > riseHeight) { age[i] = -1; b.gameObject.SetActive(false); }
            }
        }

        void Spawn()
        {
            for (int i = 0; i < Pool; i++)
            {
                if (age[i] >= 0) continue;
                age[i] = 0;
                float s = 0.003f + 0.002f * ((i * 7) % 5) / 4f;
                bubbles[i].localScale = Vector3.one * s;
                bubbles[i].gameObject.SetActive(true);
                return;
            }
        }
    }
}
