using Khkt.Interaction;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>Bình nón lắc tròn quanh đáy khi đang lắc (chỉ là phần nhìn; trộn do LabContainer tính).</summary>
    public class FlaskSwirlVisual : MonoBehaviour
    {
        public SwirlDetector swirl;
        public float maxTiltDeg = 7f;
        public float turnsPerSecond = 2f;

        Quaternion baseRotation;
        float amount, phase;

        void Update()
        {
            float target = swirl != null ? swirl.Intensity : 0f;
            if (amount <= 0.001f && target <= 0f)
            {
                baseRotation = transform.rotation;  // bình có thể vừa được cầm đi, đặt lại chỗ khác
                return;
            }
            amount = Mathf.MoveTowards(amount, target, 4f * Time.deltaTime);
            phase += turnsPerSecond * 2 * Mathf.PI * Time.deltaTime;
            float tilt = maxTiltDeg * amount;
            transform.rotation = Quaternion.Euler(tilt * Mathf.Cos(phase), 0, tilt * Mathf.Sin(phase)) * baseRotation;
        }
    }
}
