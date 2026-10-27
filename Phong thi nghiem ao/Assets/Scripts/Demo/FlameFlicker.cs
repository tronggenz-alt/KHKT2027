using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>Ngọn lửa đèn cồn lay động nhẹ.</summary>
    public class FlameFlicker : MonoBehaviour
    {
        public Light flameLight;
        public float baseIntensity = 0.6f;
        Vector3 baseScale;
        float seed;

        void Awake()
        {
            baseScale = transform.localScale;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * 6f);
            transform.localScale = new Vector3(baseScale.x * (0.9f + 0.2f * n), baseScale.y * (0.85f + 0.3f * n), baseScale.z * (0.9f + 0.2f * n));
            if (flameLight != null) flameLight.intensity = baseIntensity * (0.8f + 0.4f * n);
        }
    }
}
