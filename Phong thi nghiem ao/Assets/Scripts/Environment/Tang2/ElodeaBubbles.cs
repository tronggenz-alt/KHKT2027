using Khkt.Simulation.Bio;
using UnityEngine;

namespace Khkt.Environment.Tang2
{
    public class ElodeaBubbles : MonoBehaviour
    {
        [Header("Nguồn sáng & Cành rong")]
        public Transform lampTransform;
        public Transform stemOrigin;
        public float lampPowerWatts = 60f;

        [Header("Bọt khí")]
        public GameObject bubblePrefab;
        public float bubbleRiseSpeed = 0.08f;
        public float maxRiseHeight = 0.22f;

        float timer = 0f;

        void Update()
        {
            float distanceMeters = 0.3f;
            if (lampTransform != null && stemOrigin != null)
            {
                distanceMeters = Mathf.Max(0.05f, Vector3.Distance(lampTransform.position, stemOrigin.position));
            }

            // Cường độ sáng theo luật bình phương nghịch (W/m2)
            double intensity = Photosynthesis.LightIntensityWattsPerM2(lampPowerWatts, distanceMeters);
            double bpm = Photosynthesis.BubbleRatePerMinute(intensity);

            if (bpm <= 0.01) return;

            float intervalSeconds = (float)(60.0 / bpm);
            timer += Time.deltaTime;

            if (timer >= intervalSeconds)
            {
                timer = 0f;
                SpawnBubble();
            }
        }

        void SpawnBubble()
        {
            if (stemOrigin == null) return;
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            b.name = "BotO2";
            b.transform.position = stemOrigin.position;
            b.transform.localScale = Vector3.one * 0.005f; // Bọt khí đường kính 5mm

            var mr = b.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    color = new Color(0.85f, 0.95f, 1.0f, 0.5f)
                };
            }

            var rb = b.AddComponent<BubbleMover>();
            rb.riseSpeed = bubbleRiseSpeed;
            rb.maxHeight = stemOrigin.position.y + maxRiseHeight;
        }

        class BubbleMover : MonoBehaviour
        {
            public float riseSpeed = 0.08f;
            public float maxHeight;

            void Update()
            {
                transform.position += Vector3.up * (riseSpeed * Time.deltaTime);
                if (transform.position.y >= maxHeight)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
