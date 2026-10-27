using System;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Rót bằng cách NGHIÊNG dụng cụ. Mô hình (bản Python có kiểm thử: khkt/pour.py):
    ///   mực chất lỏng H = V / (πr²); khi nghiêng θ, mép chất lỏng dâng thêm r·tanθ;
    ///   tràn khi H + r·tanθ > h; phần vượt e chảy theo đập tràn Q = k·e^1.5.
    /// Nên cốc càng vơi càng phải nghiêng nhiều, và nghiêng quá tay thì chảy ồ ạt, giống thật.
    /// k cần hiệu chỉnh bằng cách quay video rót cốc thật (docs/04-kiem-thu-tuan-1.md).
    /// Dùng cho tay cầm VR (nghiêng thật). Chế độ máy tính rót theo tốc độ cố định (HandController).
    /// </summary>
    public class PourableContainer : MonoBehaviour
    {
        [Header("Chất lỏng")]
        public LabContainer container;

        [Header("Hình dạng (cốc 100 ml mặc định)")]
        public float radiusM = 0.025f;
        public float heightM = 0.07f;

        [Header("Dòng chảy")]
        public float weirCoef = 0.036f;
        public float minExcessM = 0.0005f;
        public float maxExcessM = 0.03f;
        [Tooltip("Miệng rót; chất lỏng rơi thẳng xuống từ đây")]
        public Transform spout;
        public LayerMask receiverMask = ~0;

        public float TiltDeg => Vector3.Angle(transform.up, Vector3.up);
        public float FlowMlPerS { get; private set; }
        public float SpilledMl { get; private set; }

        /// <summary>Chất lỏng rơi ra ngoài (xuống bàn): dùng cho hệ thống an toàn.</summary>
        public event Action<float> Spilled;

        float Area => Mathf.PI * radiusM * radiusM;
        float VolumeMl => container != null ? container.VolumeMl : 0f;
        float LevelM => Mathf.Min(heightM, VolumeMl * 1e-6f / Area);

        public float StartAngleDeg => Mathf.Atan2(heightM - LevelM, radiusM) * Mathf.Rad2Deg;

        public float FlowFor(float tiltDeg)
        {
            if (VolumeMl <= 0) return 0;
            float t = Mathf.Clamp(tiltDeg, 0, 89) * Mathf.Deg2Rad;
            float excess = LevelM + radiusM * Mathf.Tan(t) - heightM;
            if (excess <= minExcessM) return 0;
            return weirCoef * Mathf.Pow(Mathf.Min(excess, maxExcessM), 1.5f) * 1e6f;
        }

        void Update()
        {
            FlowMlPerS = FlowFor(TiltDeg);
            float outMl = Mathf.Min(VolumeMl, FlowMlPerS * Time.deltaTime);
            if (outMl <= 0) return;
            var poured = container.Take(outMl);

            Transform from = spout != null ? spout : transform;
            if (Physics.Raycast(from.position, Vector3.down, out RaycastHit hit, 0.5f, receiverMask, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<LabContainer>() is LabContainer r && r != container)
            {
                r.Add(poured);
            }
            else
            {
                SpilledMl += outMl;
                Spilled?.Invoke(outMl);
            }
        }
    }
}
