using Khkt.Input;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Nhận biết động tác LẮC TRÒN bình nón từ con quay: lắc tròn làm vận tốc góc quanh hai
    /// trục ngang dao động đều (khoảng 1-3 vòng/s). Đo độ lệch chuẩn của chúng trong cửa sổ
    /// ngắn rồi quy về cường độ 0..1. TitrationFlask dùng cường độ này để khuấy trộn.
    /// </summary>
    public class SwirlDetector : MonoBehaviour
    {
        public ControllerSource source;
        public float windowS = 0.75f;
        [Tooltip("Dưới mức này coi như không lắc (°/s)")]
        public float minDps = 30f;
        [Tooltip("Từ mức này trở lên là lắc mạnh hết cỡ (°/s)")]
        public float fullDps = 150f;

        /// <summary>Cường độ lắc 0..1: lấy lớn hơn giữa cảm biến và lệnh lắc của chế độ máy tính.</summary>
        public float Intensity => Mathf.Max(sensorIntensity, ManualIntensity);
        /// <summary>Chế độ máy tính: giữ phím lắc thì đặt 1, thả ra đặt 0.</summary>
        public float ManualIntensity { get; set; }

        float sensorIntensity;
        Vector2[] ring;
        int count, head;

        void OnEnable()
        {
            ring = new Vector2[Mathf.CeilToInt(windowS * 100)];
            if (source != null) source.SampleReceived += OnSample;
        }

        void OnDisable()
        {
            if (source != null) source.SampleReceived -= OnSample;
        }

        void OnSample(ControllerState s)
        {
            ring[head] = new Vector2(s.GyroDps.x, s.GyroDps.y);  // hai trục ngang của tay cầm
            head = (head + 1) % ring.Length;
            count = Mathf.Min(count + 1, ring.Length);
            if (count < ring.Length / 2) return;

            Vector2 mean = Vector2.zero;
            for (int i = 0; i < count; i++) mean += ring[i];
            mean /= count;
            float sum = 0;
            for (int i = 0; i < count; i++) sum += (ring[i] - mean).sqrMagnitude;
            float rms = Mathf.Sqrt(sum / count);
            sensorIntensity = Mathf.Clamp01((rms - minDps) / (fullDps - minDps));
        }
    }
}
