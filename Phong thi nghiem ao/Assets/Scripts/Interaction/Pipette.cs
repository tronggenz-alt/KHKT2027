using Khkt.Simulation;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Pipet bầu (pipet chính xác) 25 ml có quả bóp cao su. Một vạch định mức duy nhất: hút lên quá vạch
    /// một chút, xả bớt cho đáy mặt khum chạm vạch, rồi mới xả vào bình. Hút lố mà không chỉnh thì
    /// thể tích axit sai, kết quả chuẩn độ sai theo.
    /// </summary>
    public class Pipette : MonoBehaviour
    {
        public LabContainer container;
        public float nominalMl = 25f;
        [Tooltip("Độ cao vạch định mức trong hệ tọa độ pipet (m)")]
        public float markHeightM = 0.33f;
        public Transform tip;
        public float suckRateMlPerS = 5f;
        public float drainRateMlPerS = 3f;
        [Tooltip("Xả bớt chậm để chỉnh đúng vạch")]
        public float trimRateMlPerS = 0.4f;

        public float VolumeMl => container != null ? container.VolumeMl : 0f;
        public float LevelM => container != null && container.fill != null ? container.fill.LevelM : 0f;
        /// <summary>Mặt khum cách vạch bao nhiêu mm (dương: trên vạch).</summary>
        public float OffsetFromMarkMm => VolumeMl > 0.05f ? (LevelM - markHeightM) * 1000f : -999f;
        public bool IsEmpty => VolumeMl < 0.05f;

        /// <summary>Hút từ nguồn trong dt giây. Trả về ml đã hút.</summary>
        public float Suck(LabContainer source, float dt)
        {
            if (source == null || source.VolumeMl < 0.1f) return 0;
            float ml = Mathf.Min(suckRateMlPerS * dt, source.VolumeMl - 0.05f, container.FreeMl);
            if (ml <= 0) return 0;
            container.Add(source.Take(ml));
            return ml;
        }

        /// <summary>Xả vào target (null = xả ra ngoài) trong dt giây. trim = xả chậm để chỉnh vạch.</summary>
        public Solution Drain(LabContainer target, float dt, bool trim = false)
        {
            float ml = Mathf.Min((trim ? trimRateMlPerS : drainRateMlPerS) * dt, VolumeMl);
            if (ml <= 0) return default;
            var s = container.Take(ml);
            if (target != null) target.Add(s);
            return s;
        }
    }
}
