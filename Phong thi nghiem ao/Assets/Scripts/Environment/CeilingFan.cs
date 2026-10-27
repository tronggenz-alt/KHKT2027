using UnityEngine;

namespace Khkt.Environment
{
    /// <summary>Quay cánh quạt (quạt trần, quạt hút). Gắn lên GameObject trục quay của quạt.</summary>
    public class CeilingFan : MonoBehaviour
    {
        public bool running = true;
        [Tooltip("Vòng/phút. Quạt quay nhanh trông nhấp nháy trong kính VR, nên để vừa phải")]
        public float rpm = 90f;
        public Vector3 localAxis = Vector3.up;
        [Tooltip("Số giây để đạt tốc độ khi bật")]
        public float spinUpSeconds = 3f;

        float current;

        void Update()
        {
            float target = running ? rpm : 0f;
            current = Mathf.MoveTowards(current, target, rpm / Mathf.Max(0.01f, spinUpSeconds) * Time.deltaTime);
            transform.Rotate(localAxis, current * 6f * Time.deltaTime, Space.Self);  // 1 vòng/phút = 6 °/s
        }
    }
}
